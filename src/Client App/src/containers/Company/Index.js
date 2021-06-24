import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Col, Row, Spinner, Button } from 'react-bootstrap';
import { getCompany } from '../../store/company-actions';

import ReactDataGrid from '@inovua/reactdatagrid-community';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faCheck, faTimes, faEdit, faTrash, faUserShield } from '@fortawesome/free-solid-svg-icons'

import '../../App.scss';
import EditCompany from './EditCompany';
import DeleteCompany from './DeleteCompany';
import AddCompany from './AddCompany';
import ManageAdmin from './ManageAdmin';

import _ from 'lodash';

import { idle, succeeded } from '../../utils/stateStatus';

const gridStyle = { minHeight: 485, marginTop: 10 };

const Company = () => {

    const columns = [

        { name: 'name', defaultFlex: 1, header: 'Name' },
        { name: 'registrationNo', defaultFlex: 0.5, header: 'Reg. No.' },
        { name: 'email', defaultFlex: 1, header: 'Email' },
        {
            name: 'address', header: 'Address', defaultFlex: 2,
            render: ({ value }) => `${value.streetAddress}, ${value.addressLine2}, ${value.city}, ${value.country}`
        },
        {
            name: 'isActive', defaultFlex: 0.5, header: 'Active', className: "text-center",
            render: ({ value }) => <div className="text-center"> {value ?
                <FontAwesomeIcon icon={faCheck} className="text-success" />
                : <FontAwesomeIcon icon={faTimes} className="text-danger" />} </div>
        },
        {
            header: 'Edit',
            name: 'id',
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

            header: 'Admin',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => { adminHandler(data) }}>
                            <FontAwesomeIcon icon={faUserShield} className="text-light" />
                        </Button>
                    </div>
                )
            }

        },
        {

            header: 'Delete',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => { deleteHandler(data) }}>
                            <FontAwesomeIcon icon={faTrash} className="text-light" />
                        </Button>
                    </div>
                )
            }

        }
    ];

    const dispatch = useDispatch();

    const { status, companyInfo } = useSelector(state => state.company);
    const [skip, setSkip] = useState(0);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [showAddModal, setShowAddModal] = useState(false);
    const [showAdminModal, setShowAdminModal] = useState(false);

    const [editData, setEditData] = useState({});
    const [deleteData, setDeleteData] = useState({});
    const [adminData, setAdminData] = useState({});


    const addHandler = () => {

        setShowAddModal(true);
    }

    const editHandler = (data) => {
        setEditData(data);
        setShowEditModal(true);
    }

    const deleteHandler = (data) => {

        setDeleteData(data);
        setShowDeleteModal(true);
    }

    const adminHandler = (data) => {

        setAdminData(data.id);
        setShowAdminModal(true);
    }

    const addModalCloseHandler = () => {
        setShowAddModal(false);
    }

    const editModalCloseHandler = () => {
        setShowEditModal(false);
    }

    const deleteModalCloseHandler = () => {
        setShowDeleteModal(false);
    }

    const adminModalCloseHandler = () => {
        setShowAdminModal(false);
    }

    const skipChangeHandler = (skipValue) => {

        const pageNumberValue = (skipValue / 10) + 1;
        dispatch(getCompany({ pageNumber: pageNumberValue, pageSize: 10 }))
        setSkip(skipValue);
    }

    useEffect(() => {

        if(status === idle)
        {
            dispatch(getCompany())
        }
        

    }, [dispatch, status])


    return (
        <div>
            <Row className="mt-2">
                <Col md={10}>
                    <h1 className="display-4"> Company </h1>
                </Col>
                <Col md={2}>
                    <Button type="button" variant="outline-dark" className='mt-4 float-right' onClick={() => { addHandler() }}>Add Company</Button>
                </Col>
            </Row>
            <hr />
            <AddCompany show={showAddModal} onClose={addModalCloseHandler} skip={skip} />
            {showEditModal ? <EditCompany show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
            {showDeleteModal ? <DeleteCompany show={showDeleteModal} onClose={deleteModalCloseHandler} data={deleteData} /> : ''}
            {showAdminModal ? <ManageAdmin show={showAdminModal} onClose={adminModalCloseHandler} data={adminData} /> : ''}
            <Row>
                {
                    status === succeeded ?
                        !_.isEmpty(companyInfo)
                            ? <ReactDataGrid
                                idProperty="id"
                                style={gridStyle}
                                columns={columns}
                                pagination
                                skip={skip}
                                sortable={false}
                                showColumnMenuTool={false}
                                onSkipChange={skipChangeHandler}
                                pageSizes={false}
                                theme="default-light"
                                dataSource={new Promise(function (resolve, reject) {
                                    resolve(companyInfo)
                                })}
                                defaultLimit={10}
                            />

                            : <h4 className="blockquote center">No Data</h4>
                        : <Spinner animation="border spinner" />
                }
            </Row>
        </div>
    )
};

export default Company;