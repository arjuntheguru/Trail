import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Modal } from 'react-bootstrap';

import { getUser } from '../../store/user-actions';
import ReactDataGrid from '@inovua/reactdatagrid-community';
import { Spinner, Button } from 'react-bootstrap';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faKey, faEdit } from '@fortawesome/free-solid-svg-icons'

import '../../App.css';
import EditAdmin from './EditAdmin';
import AddAdmin from './AddAdmin';
import ChangeAdminPassword from './ChangeAdminPassword';
import { succeeded } from '../../utils/stateStatus';

import _ from 'lodash';

const gridStyle = { minHeight: 485, marginTop: 10 };

const ManageAdmin = ({ show, onClose, data }) => {

    const columns = [

        { name: 'firstName', defaultFlex: 0.8, header: 'First Name' },
        { name: 'lastName', defaultFlex: 0.8, header: 'Last Name' },
        { name: 'userName', defaultFlex: 0.8, header: 'Username' },
        { name: 'email', defaultFlex: 1, header: 'Email' },
        {
            header: 'Edit',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => editHandler(data)}>
                            <FontAwesomeIcon icon={faEdit} className="text-light" />
                        </Button>
                    </div>
                )
            }
        },
        {
            header: 'Pass',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => changePasswordHandler(data)} >
                            <FontAwesomeIcon icon={faKey} className="text-light" />
                        </Button>
                    </div>
                )
            }
        }

    ]

    const dispatch = useDispatch();

    const { status, userInfo } = useSelector(state => state.user)

    const [showAddModal, setShowAddModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);

    const [editData, setEditData] = useState({});
    const [userName, setUserName] = useState('');

    const addHandler = () => {

        setShowAddModal(true);
    }

    const editHandler = (data) => {

        setEditData(data);
        setShowEditModal(true);
    }

    const changePasswordHandler = (data) => {

        setUserName(data.userName);
        setShowChangePasswordModal(true);
    }

    const editModalCloseHandler = () => {

        setShowEditModal(false);
    }

    const addModalCloseHandler = () => {
        setShowAddModal(false);
    }

    const changePasswordModalCloseHandler = () => {
        setShowChangePasswordModal(false);
    }


    useEffect(() => {

        dispatch(getUser(data, "Admin"))

    }, [dispatch, data]);

    return (
        <div>
            {showEditModal ? <EditAdmin show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
            {showAddModal ? <AddAdmin show={showAddModal} onClose={addModalCloseHandler} data={data} /> : ''}
            {showChangePasswordModal ? <ChangeAdminPassword show={showChangePasswordModal} onClose={changePasswordModalCloseHandler} data={userName} /> : ''}

            <Modal show={show} onHide={onClose} centered size="lg">
                <Modal.Header closeButton={false} className="bg-dark">
                    <Modal.Title className="text-light w-100">
                        Admin
                    <Button type="button" variant="outline-light" className="float-right" onClick={() => { addHandler() }}>Add Admin</Button>
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body className="bgGradient" style={{ minHeight: "550px" }}>
                    {
                        status === succeeded ?
                            !_.isEmpty(userInfo)
                                ? <ReactDataGrid
                                    idProperty="id"
                                    style={gridStyle}
                                    columns={columns}
                                    pagination
                                    sortable={false}
                                    showColumnMenuTool={false}
                                    theme="default-light"
                                    pageSizes={false}
                                    dataSource={userInfo}
                                    defaultLimit={10}
                                />

                                : <h4 className="text-muted center">No Data</h4>
                            : <Spinner animation="border spinner" />
                    }
                </Modal.Body>
            </Modal>
        </div>
    )
};

export default ManageAdmin;