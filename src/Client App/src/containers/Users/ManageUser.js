import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Col, Row, Spinner, Button, Table, Badge } from 'react-bootstrap';

import { getUser, deleteUser } from '../../store/user-actions';
import { getSiteWithoutPagination } from '../../store/site-actions';
import { failed, idle, succeeded } from '../../utils/stateStatus';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faEdit, faTrash, faTrashRestore } from '@fortawesome/free-solid-svg-icons'

import AddUser from './AddUser';
import EditUser from './EditUser';
import DeleteUser from './DeleteUser';

const ManageUser = () => {

    const dispatch = useDispatch();

    const { companyId } = useSelector(state => state.auth.user);
    const { status, userInfo } = useSelector(state => state.user);
    const { status: siteStatus, siteInfo, recentGETMethod } = useSelector(state => state.site);

    const [showAddModal, setShowAddModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);

    const [editData, setEditData] = useState({});
    const [deleteData, setDeleteData] = useState({});
    

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

    const restoreHandler = (data) => {

        var res = window.confirm(`Restore user ${data.firstName} ${data.lastName}`)
        console.log(res)
        if(res) {
            dispatch(deleteUser(data, true));
        }
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

    useEffect(() => {

        if (siteStatus === idle || (siteStatus === succeeded && recentGETMethod !== "getSiteWithoutPagination")) {
            dispatch(getSiteWithoutPagination(companyId))
        }

    }, [dispatch, companyId, siteStatus, recentGETMethod])

    useEffect(() => {

        if (status === idle || status === failed) {
            dispatch(getUser(companyId))
        }

    }, [dispatch, companyId, status])   


    return (
        <div>
            {
                status === succeeded ? (
                    <>
                        <Row className="mt-2">
                            <Col md={10}>
                                <h1 className="display-4">Manage User</h1>
                            </Col>
                            <Col md={2}>
                                <Button type="button" variant="outline-dark" className='mt-4 float-right' onClick={() => { addHandler() }}>Add User</Button>
                            </Col>
                        </Row>
                        <hr />
                        {siteStatus === succeeded && (<> 
                        {showAddModal ? <AddUser show={showAddModal} onClose={addModalCloseHandler} siteInfo={siteInfo} data={companyId} /> : ''}
                        {showEditModal ? <EditUser show={showEditModal} onClose={editModalCloseHandler} siteInfo={siteInfo} data={editData} /> : ''}
                        {showDeleteModal ? <DeleteUser show={showDeleteModal} onClose={deleteModalCloseHandler} data={deleteData} /> : ''}
                        <Row>
                            <Table bordered striped size="" >
                                <thead className="bg-dark text-light">
                                    <tr>
                                        <th>Name</th>
                                        <th>Email</th>
                                        <th>Username</th>
                                        <th>Status</th>
                                        <th>Role</th>
                                        <th>Access</th>
                                        <th></th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {userInfo.map((user) => {
                                        return (
                                            <tr className="" style={{fontSize:"14px"}}>
                                                <td>{`${user.firstName} ${user.lastName}`}</td>
                                                <td>{user.email}</td>
                                                <td>{user.userName}</td>
                                                <td>{
                                                    user.isActive ? 
                                                    <Badge pill variant="success" className="px-3 py-2 mx-1">
                                                        Active
                                                    </Badge> : 
                                                    <Badge pill variant="danger" className="px-3 py-2 mx-1">
                                                        Inactive
                                                    </Badge>
                                                    }</td>
                                                <td>{user.role}</td>
                                                <td className="text-muted">{user.siteId == null ?
                                                    "All Sites" :
                                                    user.siteId.map((siteId, i) => {
                                                        if (i < 2) {
                                                            return (
                                                                <span>{i === 0 ? '' : ', '}{siteInfo.data.find((item) => item.id === siteId).name}</span>

                                                            )
                                                        }
                                                        else {
                                                            if (!user.siteId[i + 1]) {
                                                                return (
                                                                    <span>, +{i - 1} more</span>
                                                                )
                                                            }
                                                        }
                                                    })} </td>
                                                <td className="text-center"><Button type="button" variant="dark" onClick={() => {editHandler(user)}}><FontAwesomeIcon icon={faEdit} className="text-light"/></Button></td>
                                                <td className="text-center">
                                                    {
                                                        user.isActive ? 
                                                        <Button type="button" 
                                                            variant="danger" 
                                                            onClick={() => {deleteHandler(user)}}>
                                                                <FontAwesomeIcon icon={faTrash} className="text-light"/>
                                                        </Button> :
                                                         <Button type="button" 
                                                         variant="success" 
                                                         onClick={() => {restoreHandler(user)}}>
                                                             <FontAwesomeIcon icon={faTrashRestore} className="text-light"/>
                                                     </Button> 
                                                    }
                                                </td>
                                            </tr>
                                        )
                                    })}
                                </tbody>
                            </Table>
                        </Row>
                        </>)}
                    </>
                ) : <Spinner animation="border spinner" />
            }

        </div>
    )

};

export default ManageUser;